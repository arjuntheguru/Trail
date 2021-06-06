import React from 'react';
import { Modal } from 'react-bootstrap';
import UserForm from './UserForm';

const AddUser = ({ show, onClose, siteInfo, data }) => {
    
    return (
        <Modal show={show} onHide={onClose} centered size="lg" >          
            <Modal.Body className="bgGradient">{<UserForm onClose={onClose} siteInfo={siteInfo} data={data}/>}</Modal.Body>
        </Modal>
    )
};

export default AddUser;