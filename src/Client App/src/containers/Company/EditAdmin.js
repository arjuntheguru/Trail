import React from 'react';
import { Modal } from 'react-bootstrap';
import AdminForm from './AdminForm';

const EditCompany = ({ show, onClose, data }) => {

    return (
        <Modal show={show} onHide={onClose} centered size="lg" className="bg-transparent">
            <Modal.Body className="bgGradient">{<AdminForm isEdit={true} data={data} onClose={onClose}/>}</Modal.Body>
        </Modal>
    )

}

export default EditCompany;