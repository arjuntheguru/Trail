import React from 'react';
import { Modal, Button } from 'react-bootstrap';

import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faExclamationCircle } from '@fortawesome/free-solid-svg-icons';

const DeleteModal = ({show, onClose, data, handler}) => {    

    return (
        <Modal show={show} onHide={onClose} centered size="sm" >
            {/* <Modal.Header closeButton className="bg-danger text-light">
                <Modal.Title>{data.name}</Modal.Title>
            </Modal.Header> */}
            <Modal.Body className="text-center bgGradient">              
                    <FontAwesomeIcon icon={faExclamationCircle} size='4x' className="text-danger mt-4" />
                    <br></br>
                    <h3 className="mt-3">Are you sure ?</h3>                    
                    <h5>Do you want to archive <span className="font-weight-bold">"{data}"</span> ? </h5>
                    <br></br>
                    <div className="m-1">
                        <Button type="button" variant="danger" className="mx-2" onClick={handler}>
                            Archive
                        </Button>
                        <Button variant="primary" className="mx-2" onClick={onClose}>
                            Cancel
                        </Button>
                    </div>
               
            </Modal.Body>
        </Modal>
    )
}

export default DeleteModal